using Microsoft.Xna.Framework;

namespace Ironfield;

/// MapScreen: the promotion screen for one troop stack (2.2). The lines these men may rise through are drawn as a tree,
/// a column a tier, from the men they are now to the top; the troop under the mouse (or the one picked) is told in full
/// beside your men's own numbers, with what it costs to promote 1, 5 or every ready man.
partial class MapScreen
{
    /// The troop picked in the tree ("" = the first branch).
    string upPick = "";

    /// One troop in the tree: its column (tiers above your men), the troops it leads to, and where it is drawn.
    sealed class TreeNode
    {
        public string Id = "";
        public UnitType T = null!;
        public int Col;
        public List<string> Kids = new();
        /// A branch your men can take now (a child of the root); barred by a rule (noble birth, an order's own).
        public bool Option, Locked;
        /// Why the way is barred, when it is.
        public string Why = "";
        public Rectangle Box;
    }

    /// Every troop these men can unlock or upgrade into: breadth first from the stack's troop, excluding locked lines.
    Dictionary<string, TreeNode> TroopTree(CompanyRecord c, TroopStack st)
    {
        var graph = C.UpgradeGraph(C.Player, c, st.Troop);
        var nodes = new Dictionary<string, TreeNode>();
        foreach (var (id, kids) in graph)
        {
            var t = Troops.Get(id);
            nodes[id] = new TreeNode { Id = id, T = t, Col = id == st.Troop ? 0 : Math.Max(1, t.Tier - st.Type.Tier), Kids = kids };
        }
        foreach (var kid in nodes[st.Troop].Kids) nodes[kid].Option = true;
        return nodes;
    }

    /// Lay the tree out in `area`: a column a tier, each column's troops ordered under the ones that lead to them (common
    /// lines first among the branches), so the lines between cross as little as may be.
    void LayTree(Dictionary<string, TreeNode> nodes, string root, Rectangle area)
    {
        int cols = nodes.Values.Max(n => n.Col) + 1;
        var order = new Dictionary<string, float> { [root] = 0 };
        var byCol = new List<List<TreeNode>>();
        for (int col = 0; col < cols; col++)
        {
            var list = nodes.Values.Where(n => n.Col == col).ToList();
            float Rank(TreeNode n)
            {
                var parents = nodes.Values.Where(p => p.Col < col && p.Kids.Contains(n.Id) && order.ContainsKey(p.Id)).ToList();
                float r = parents.Count == 0 ? 0 : parents.Average(p => order[p.Id] + p.Kids.IndexOf(n.Id) * 0.01f);
                return r + (col == 1 && n.T.Culture != Factions.Common ? 100 : 0);
            }
            list = list.OrderBy(Rank).ThenBy(n => n.T.Class).ThenBy(n => n.Id).ToList();
            for (int i = 0; i < list.Count; i++) order[list[i].Id] = i - (list.Count - 1) / 2f;
            byCol.Add(list);
        }
        int most = byCol.Max(l => l.Count), gapX = ui.U(cols > 5 ? 20 : 40);
        int nw = Math.Min(ui.U(168), (area.Width - (cols - 1) * gapX) / cols), nh = Math.Clamp(area.Height / most - ui.U(4), ui.U(18), ui.U(34));
        if (nh < ui.U(28) && area.Height / most >= ui.U(30)) nh = ui.U(28); // room for a name on two lines
        int step = Math.Min(nh + ui.U(6), Math.Max(nh + ui.U(2), area.Height / most)), x0 = area.X + (area.Width - cols * nw - (cols - 1) * gapX) / 2;
        for (int col = 0; col < cols; col++)
            for (int i = 0; i < byCol[col].Count; i++)
                byCol[col][i].Box = new Rectangle(x0 + col * (nw + gapX), area.Center.Y - byCol[col].Count * step / 2 + i * step + (step - nh) / 2, nw, nh);
    }

    void UpgradePanel()
    {
        var c = upgrading!;
        var st = upStack;
        if (st == null || !c.Stacks.Contains(st) || !C.Player.Companies.Contains(c)) { panel = PanelKind.Party; upStack = null; return; }
        var nodes = TroopTree(c, st);
        var root = nodes[st.Troop];
        var r = Modal(1240, 680, $"{st.Type.Name.ToUpperInvariant()} OF {c.Name.ToUpperInvariant()}");
        int mx = r.X + ui.U(20), mw = r.Width - ui.U(40);

        // Your men: how many, their pool of XP, how many are ready.
        var top = Card(new Rectangle(mx, r.Y + ui.U(28), mw, ui.U(46)), Kit.Gilt);
        Stat(top.X, top.Y, "helm", $"{st.Men}", "FIT", Style.Text);
        Stat(top.X + ui.U(90), top.Y, "heart", $"{st.Wounded}", "WOUNDED", st.Wounded > 0 ? Style.Warn : Style.Dim);
        Stat(top.X + ui.U(200), top.Y, "up", $"{st.Ready}", "READY TO RISE", st.Ready > 0 ? Kit.Gilt : Style.Dim);
        int gx = top.X + ui.U(340), gw = top.Right - ui.U(170) - gx;
        ui.Text("THE POOL OF XP", gx, top.Y, Style.Dim, 1);
        ui.TextR(st.PoolCap > 0 ? $"{st.Xp:0} / {st.PoolCap:0}   {st.Type.UpgradeXp} A MAN" : "AT THE TOP OF THEIR LINE", gx + gw, top.Y, Style.Dim, 1);
        ui.Gauge(new Rectangle(gx, top.Y + ui.U(13), gw, ui.U(8)), st.PoolCap <= 0 ? 0 : st.Xp / st.PoolCap, st.Ready > 0 ? Kit.Gilt : Style.Friendly);
        Stat(top.Right - ui.U(130), top.Y, "coin", $"{C.S.Gold:N0}", "CROWNS", Kit.Gilt);

        // The tree.
        var area = new Rectangle(mx, r.Y + ui.U(98), mw, ui.U(340));
        ui.Rect(new Rectangle(area.X, area.Y - ui.U(18), area.Width, area.Height + ui.U(24)), Color.Black * 0.22f);
        LayTree(nodes, root.Id, area);
        if (!nodes.ContainsKey(upPick) || upPick == root.Id) upPick = root.Kids.FirstOrDefault(k => C.Promotable(C.Player, c, st, k, 1, C.S.Gold, out _, out _) > 0) ?? root.Kids.FirstOrDefault() ?? root.Id;
        var hover = nodes.Values.FirstOrDefault(n => ui.Hover(n.Box));
        var focus = hover ?? nodes[upPick];
        // What leads to the troop in focus, and what it leads on to.
        var before = new HashSet<string> { focus.Id };
        for (bool grew = true; grew;)
        {
            grew = false;
            foreach (var n in nodes.Values) if (!before.Contains(n.Id) && n.Kids.Any(before.Contains)) { before.Add(n.Id); grew = true; }
        }
        var after = new HashSet<string> { focus.Id };
        var walk = new Queue<string>(new[] { focus.Id });
        while (walk.Count > 0) foreach (var k in nodes[walk.Dequeue()].Kids) if (after.Add(k)) walk.Enqueue(k);

        // Tier headings over the columns.
        foreach (var g in nodes.Values.GroupBy(n => n.Col))
        {
            var b = g.First().Box;
            int tier = Math.Clamp(st.Type.Tier + g.Key, 0, 7);
            ui.TextC(g.Key == 0 ? "YOUR MEN" : $"TIER {Style.Numerals[tier]}", b.Center.X, area.Y - ui.U(13), g.Key == 0 ? Kit.Gilt : Style.Dim, 1);
        }
        // The lines: faint, then the way to the troop in focus in gilt and the way on from it in parchment.
        int px = Math.Max(1, ui.U(1));
        for (int pass = 0; pass < 2; pass++)
            foreach (var a in nodes.Values)
                foreach (var kid in a.Kids)
                {
                    var b = nodes[kid];
                    bool to = before.Contains(a.Id) && before.Contains(b.Id), from = after.Contains(a.Id) && after.Contains(b.Id);
                    if ((to || from) != (pass == 1)) continue;
                    var col = to ? Kit.Gilt : from ? Kit.Mul(Kit.Parchment, 0.75f) : Color.White * 0.11f;
                    int t = to ? px * 2 : px, midX = b.Box.X - (b.Box.X - a.Box.Right) / 2 - (b.Col - a.Col > 1 ? ui.U(6) : 0), ay = a.Box.Center.Y, by = b.Box.Center.Y;
                    ui.Rect(new Rectangle(a.Box.Right, ay - t / 2, midX - a.Box.Right + t, t), col);
                    ui.Rect(new Rectangle(midX, Math.Min(ay, by) - t / 2, t, Math.Abs(by - ay) + t), col);
                    ui.Rect(new Rectangle(midX, by - t / 2, b.Box.X - midX, t), col);
                }
        foreach (var n in nodes.Values) TreeTile(n, n == root, n == focus, n.Id == upPick, before.Contains(n.Id) || after.Contains(n.Id), c, st);
        if (hover != null && hover != root && ui.Clicked(hover.Box)) upPick = hover.Id;

        // The troop in focus, told in full.
        TroopDetail(new Rectangle(mx, r.Y + ui.U(450), mw, ui.U(150)), focus, nodes, root, c, st);
        if (!c.Stacks.Contains(st)) { upStack = null; panel = PanelKind.Party; return; }

        // Moving and splitting the stack, and the way back.
        int bw = ui.U(210), by2 = r.Bottom - ui.U(40);
        var target = C.Player.Companies.FirstOrDefault(o => o != c && Rules.Fits(o, st.Troop) && Rules.Places(o) + (st.Men + st.Wounded) * Rules.TierWeight(st.Type.Tier) <= Rules.Cap(Rules.RoleOf(o)) + 1e-3f);
        if (ui.Button(new Rectangle(mx, by2, bw, ui.U(26)), target != null ? ui.Fit($"MOVE TO {target.Name.ToUpperInvariant()}", bw - ui.U(8), 1) : "MOVE (NO ROOM)", target != null))
        { note = C.MoveStack(c, st, target!) ?? "MOVED"; upStack = null; panel = PanelKind.Party; return; }
        if (ui.Button(new Rectangle(mx + bw + ui.U(8), by2, bw, ui.U(26)), "SPLIT HALF OFF", st.Men >= 2))
        { note = C.SplitStack(c, st, st.Men / 2) ?? "SPLIT"; upStack = null; panel = PanelKind.Party; return; }
        if (ui.Button(new Rectangle(mx + 2 * (bw + ui.U(8)), by2, bw, ui.U(26)), "SPLIT OFF ALL", st.Men >= 1 && c.Stacks.Count > 1))
        { note = C.SplitStack(c, st, st.Men) ?? "SPLIT"; upStack = null; panel = PanelKind.Party; return; }
        if (note != "") ui.TextFitC(note, r.Center.X + ui.U(230), by2 + ui.U(9), ui.U(380), Style.Warn, 1);
        if (ui.Gilded(new Rectangle(r.Right - ui.U(200), by2 - ui.U(1), ui.U(180), ui.U(28)), "BACK", ctl.Name(Act.Menu)))
        { upStack = null; panel = at != null && C.SettlementAt(C.Player.Pos) == at ? PanelKind.Settlement : PanelKind.Party; }
    }

    /// One troop of the tree as a tile: his head, his name, his kind; gilt for your men and the branch picked, bright for
    /// a branch you can take now, dim for what lies further up, chained where a rule bars the way.
    void TreeTile(TreeNode n, bool root, bool focus, bool picked, bool onPath, CompanyRecord c, TroopStack st)
    {
        var b = n.Box;
        bool can = n.Option && C.Promotable(C.Player, c, st, n.Id, 1, C.S.Gold, out _, out _) > 0;
        bool own = n.T.Culture != Factions.Common;
        ui.Rect(new Rectangle(b.X + ui.U(1), b.Y + ui.U(2), b.Width, b.Height), Color.Black * 0.35f);
        ui.VGrad(b, root || picked ? Kit.Mul(Kit.Gilt, 0.36f) : focus ? new Color(56, 58, 66) : new Color(34, 37, 46), root || picked ? Kit.Mul(Kit.Gilt, 0.12f) : Kit.IronDark, 1);
        int ph = b.Height - ui.U(4);
        PortraitWell(n.T, new Rectangle(b.X + ui.U(2), b.Y + ui.U(2), ph, ph), 2);
        int tx = b.X + ph + ui.U(7), tw = b.Right - tx - ui.U(n.Locked ? 14 : 5);
        bool two = b.Height >= ui.U(28);
        // A name too long for one line takes both (and the line of his kind gives way), so no name is cut short.
        var nameLines = two && ui.TW(n.T.Name.ToUpperInvariant(), 1) > tw ? ui.Wrap(n.T.Name.ToUpperInvariant(), tw) : null;
        if (nameLines is { Count: > 1 })
        {
            ui.TextFit(nameLines[0], tx, b.Y + ui.U(5), tw, root || picked ? Kit.Gilt : Style.Text, 1);
            ui.TextFit(string.Join(" ", nameLines.Skip(1)), tx, b.Y + ui.U(17), tw, root || picked ? Kit.Gilt : Style.Text, 1);
        }
        else ui.TextFit(n.T.Name.ToUpperInvariant(), tx, b.Y + (two ? ui.U(5) : (b.Height - ui.U(7)) / 2), tw, root || picked ? Kit.Gilt : Style.Text, 1);
        if (two && nameLines is not { Count: > 1 }) ui.TextFit(root ? $"{st.Men} MEN" : $"{n.T.Class.ToString().ToUpperInvariant()}{(own ? " . " + n.T.Culture.ToUpperInvariant() : "")}", tx, b.Y + ui.U(17), tw, own && !root ? Kit.Mul(Style.Accent, 0.85f) : Style.Dim, 1);
        if (n.Locked) ui.Icon("chain", b.Right - ui.U(11), b.Y + (b.Height - ui.U(7)) / 2, Style.Bad, 1);
        if (!root && !onPath) ui.Rect(b, Kit.IronDark * 0.5f); // not on the way to or from the troop in focus
        else if (!root && !n.Option && !focus) ui.Rect(b, Kit.IronDark * 0.22f);
        var edge = root || picked ? Kit.Gilt : n.Locked ? Kit.Mul(Style.Bad, 0.7f) : can ? Style.Accent : focus ? Color.White * 0.5f : n.Option ? Color.White * 0.28f : Color.White * 0.1f;
        ui.Frame(b, edge, ui.U(root || picked || focus ? 2 : 1));
        if (can && !picked) ui.Rect(new Rectangle(b.X, b.Y, ui.U(2), b.Height), Style.Accent);
        if (ui.Hover(b) && !root) Ui.Hovered(b);
    }

    /// The troop in focus: his picture, his numbers as bars beside your men's (what rises in green, what falls in amber),
    /// how he fights, and the promotion itself, or which branch leads to him.
    void TroopDetail(Rectangle box, TreeNode n, Dictionary<string, TreeNode> nodes, TreeNode root, CompanyRecord c, TroopStack st)
    {
        var t = n.T;
        var now = st.Type;
        var d = Card(box, n == root ? Kit.Gilt : n.Locked ? Style.Bad : Style.Accent);
        int ph = d.Height;
        PortraitWell(t, new Rectangle(d.X, d.Y, ph, ph), 2);
        ui.Frame(new Rectangle(d.X, d.Y, ph, ph), Kit.GiltDark, ui.U(1));
        int tx = d.X + ph + ui.U(14), colW = ui.U(250);
        ui.TextFit(t.Name.ToUpperInvariant(), tx, d.Y, colW, Style.Text, ui.FitScale(t.Name.ToUpperInvariant(), colW));
        for (int i = 0; i < 7; i++) ui.Rect(new Rectangle(tx + i * ui.U(7), d.Y + ui.U(21), ui.U(5), ui.U(5)), i < t.Tier ? Kit.Gilt : Color.White * 0.1f);
        ui.Text($"TIER {Style.Numerals[Math.Clamp(t.Tier, 0, 7)]}", tx + ui.U(54), d.Y + ui.U(20), Style.Dim, 1);
        int cx = tx, cy = d.Y + ui.U(34);
        cx += ui.Chip(cx, cy, t.Class.ToString().ToUpperInvariant(), Style.Text, t.Mount != null ? "horse" : t.Range > 0 ? "arrow" : "sword") + ui.U(4);
        ui.Chip(cx, cy, (Factions.Culture(t.Culture)?.Name ?? t.Culture).ToUpperInvariant(), t.Culture == Factions.Common ? Style.Dim : Style.Accent, "flag");
        int fy = d.Y + ui.U(54);
        var (style, styleLine) = Battle.StyleText(t.Style);
        if (t.Mount != null) fy = Outcome(tx, fy, colW, "horse", $"RIDES A {t.Mount.Name.ToUpperInvariant()}");
        if (style != "") fy = Outcome(tx, fy, colW, "sword", $"{style.ToUpperInvariant()}: {styleLine.ToUpperInvariant()}", 1);
        if (t.Spells.Length > 0) fy = Outcome(tx, fy, colW, "star", "SPELLS: " + string.Join(", ", t.Spells.Select(id => Abilities.Get(id).Name.ToUpperInvariant())), 1);
        var traits = t.Passives.Select((p, i) => (e: Effects.Get(p), k: Battle.PassiveWeight(t.Tier, i))).Where(x => x.k > 0).Select(x => x.e.Name.ToUpperInvariant()).ToList();
        if (traits.Count > 0 && fy + ui.U(14) <= d.Bottom) fy = Outcome(tx, fy, colW, "shield", string.Join(", ", traits), 1);
        if (n != root && Rules.TroopRole(n.Id) != Rules.RoleOf(c) && fy + ui.U(14) <= d.Bottom) Outcome(tx, fy, colW, "flag", $"JOINS A {Rules.TroopRole(n.Id).ToUpperInvariant()} COMPANY", -1);

        // The numbers as bars, each against the best in this tree, your men's mark on the bar.
        int sx = tx + colW + ui.U(18), sw = d.Right - ui.U(300) - sx, sy = d.Y;
        var all = nodes.Values.Select(k => k.T).ToList();
        var rows = new List<(string Icon, string Label, float V, float Was, float Max, string Fmt)>
        {
            ("heart", "HEALTH", t.MaxHp, now.MaxHp, all.Max(k => k.MaxHp), "0"),
            ("shield", "ARMOUR", t.Armor, now.Armor, all.Max(k => k.Armor), "0"),
            ("sword", "DAMAGE", t.Damage, now.Damage, all.Max(k => k.Damage), "0"),
            ("spear", "REACH", t.Reach, now.Reach, all.Max(k => k.Reach), "0.0"),
            ("boot", "SPEED", t.Speed, now.Speed, all.Max(k => k.Speed), "0.0"),
        };
        if (t.Range > 0 || now.Range > 0) rows.Add(("arrow", "RANGE", t.Range, now.Range, all.Max(k => k.Range), "0"));
        int step = Math.Min(ui.U(26), d.Height / rows.Count);
        foreach (var (icon, label, v, was, max, fmt) in rows)
        {
            ui.Icon(icon, sx, sy + ui.U(2), Style.Dim, 1);
            ui.Text(label, sx + ui.U(12), sy + ui.U(2), Style.Dim, 1);
            var bar = new Rectangle(sx + ui.U(64), sy + ui.U(1), sw - ui.U(150), ui.U(9));
            float m = Math.Max(0.01f, max);
            float diff = v - was;
            ui.Rect(bar, Color.Black * 0.5f);
            int vw = (int)(bar.Width * Math.Clamp(v / m, 0, 1)), ww = (int)(bar.Width * Math.Clamp(was / m, 0, 1));
            ui.Rect(new Rectangle(bar.X, bar.Y, Math.Min(vw, ww), bar.Height), Kit.Mul(Kit.Parchment, 0.75f));
            if (vw > ww) ui.Rect(new Rectangle(bar.X + ww, bar.Y, vw - ww, bar.Height), Style.Ok);
            else if (ww > vw) ui.Rect(new Rectangle(bar.X + vw, bar.Y, ww - vw, bar.Height), Kit.Mul(Style.Warn, 0.55f));
            ui.Rect(new Rectangle(bar.X + ww - px(), bar.Y - ui.U(2), Math.Max(1, ui.U(1)), bar.Height + ui.U(4)), Color.White * 0.7f); // your men now
            ui.Text(v.ToString(fmt), bar.Right + ui.U(8), sy + ui.U(2), Style.Text, 1);
            if (n != root && MathF.Abs(diff) >= 0.05f) ui.Text((diff > 0 ? "+" : "") + diff.ToString(fmt), bar.Right + ui.U(44), sy + ui.U(2), diff > 0 ? Style.Ok : Style.Warn, 1);
            sy += step;
        }
        int px() => Math.Max(1, ui.U(1)) / 2;

        // The promotion.
        var act = new Rectangle(d.Right - ui.U(270), d.Y, ui.U(270), d.Height);
        ui.Rect(new Rectangle(act.X - ui.U(14), act.Y, ui.U(1), act.Height), Color.White * 0.08f);
        if (n == root)
        {
            ui.Text("YOUR MEN NOW", act.X, act.Y, Kit.Gilt, 1);
            string msg = root.Kids.Count == 0 ? "NO FURTHER PROMOTIONS ARE AVAILABLE FOR THESE MEN RIGHT NOW." : "PICK A BRANCH IN THE TREE TO SEE IT AGAINST THEM. THE WHITE MARK ON EACH BAR IS WHAT THEY HAVE NOW.";
            foreach (var (line, i) in ui.Wrap(msg, act.Width).Select((l, i) => (l, i)))
                ui.Text(line, act.X, act.Y + ui.U(16) + i * ui.U(11), Style.Dim, 1);
            return;
        }
        if (!n.Option)
        {
            var via = root.Kids.Where(k => Leads(nodes, k, n.Id)).Select(k => nodes[k].T.Name.ToUpperInvariant()).ToList();
            ui.Text("FURTHER UP THE LINE", act.X, act.Y, Kit.Gilt, 1);
            int ly = act.Y + ui.U(16);
            foreach (var line in ui.Wrap($"{n.T.Tier - now.Tier} PROMOTIONS FROM HERE. REACHED THROUGH {string.Join(" OR ", via)}.", act.Width).Take(6)) { ui.Text(line, act.X, ly, Style.Dim, 1); ly += ui.U(11); }
            if (n.Locked) foreach (var line in ui.Wrap(n.Why == "" ? "THE WAY IS BARRED." : n.Why, act.Width - ui.U(20)).Take(3)) { ly = Outcome(act.X, ly + ui.U(4), act.Width, "chain", line, -1) - ui.U(8); }
            return;
        }
        int one = C.Promotable(C.Player, c, st, n.Id, 1, C.S.Gold, out int cost1, out string reason);
        ui.Text("PROMOTE INTO " + ui.Fit(t.Name.ToUpperInvariant(), act.Width - ui.TW("PROMOTE INTO ", 1)), act.X, act.Y, Kit.Gilt, 1);
        if (one > 0) Price(act.X, act.Y + ui.U(16), cost1, "A MAN");
        else foreach (var (line, i) in ui.Wrap(reason, act.Width).Take(2).Select((l, i) => (l, i))) ui.Text(line, act.X, act.Y + ui.U(14) + i * ui.U(11), Style.Warn, 1);
        ui.TextR($"{st.Ready} READY", act.Right, act.Y + ui.U(16), st.Ready > 0 ? Style.Text : Style.Dim, 1);
        int bw = (act.Width - ui.U(16)) / 3, b = 0;
        foreach (var (label, want) in new[] { ("1", 1), ("5", 5), ("ALL", st.Ready) })
        {
            int k = C.Promotable(C.Player, c, st, n.Id, want, C.S.Gold, out int cost, out _);
            bool ok = k > 0 && (want == 1 || k > 1);
            var br = new Rectangle(act.X + b * (bw + ui.U(8)), act.Y + ui.U(36), bw, ui.U(40));
            if (ui.Button(br, label, ok, "", ok && want == st.Ready)) note = C.Promote(c, st, n.Id, k) ?? $"PROMOTED {k}";
            if (ok) { ui.Icon("coin", br.Center.X - ui.U(6) - ui.TW($"{cost}", 1) / 2, br.Bottom + ui.U(5), Kit.Gilt, 1); ui.Text($"{cost}", br.Center.X + ui.U(6) - ui.TW($"{cost}", 1) / 2, br.Bottom + ui.U(5), Kit.Gilt, 1); }
            if (ui.Hover(br)) note = k > 0 ? $"PROMOTE {k} FOR {cost} CROWNS" : reason;
            b++;
        }
        ui.TextFit("EACH MAN PROMOTED SPENDS HIS SHARE OF THE POOL.", act.X, act.Bottom - ui.U(9), act.Width, Style.Dim, 1);
    }

    /// Does the troop `from` lead, by any number of promotions, to `to`?
    static bool Leads(Dictionary<string, TreeNode> nodes, string from, string to)
    {
        var seen = new HashSet<string> { from };
        var walk = new Queue<string>(new[] { from });
        while (walk.Count > 0)
        {
            string id = walk.Dequeue();
            if (id == to) return true;
            foreach (var k in nodes[id].Kids) if (seen.Add(k)) walk.Enqueue(k);
        }
        return false;
    }
}
