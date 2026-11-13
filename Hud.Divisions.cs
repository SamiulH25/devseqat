using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// Hud: your divisions (docs/archive/COMBAT_OVERHAUL.md 1.7): a card per division above the player panel, the goal buttons that
/// replace the order buttons while one is picked, the goal wheel, and goal lines on the field in command view.
sealed partial class Hud
{
    readonly List<(Rectangle R, Division D)> divCards = new();
    readonly List<(Rectangle R, GoalKind G, int Side)> goals = new();
    readonly List<Rectangle> divTags = new();
    Rectangle goalClose;

    /// The goals on the buttons (command view), in order; Side is for Flank.
    public static readonly (GoalKind G, int Side, string Label)[] GoalButtons =
    {
        (GoalKind.Attack, 1, "ATTACK"), (GoalKind.Hold, 1, "HOLD"), (GoalKind.Take, 1, "TAKE GROUND"), (GoalKind.FallBack, 1, "FALL BACK"),
        (GoalKind.Flank, -1, "FLANK LEFT"), (GoalKind.Flank, 1, "FLANK RIGHT"), (GoalKind.Screen, 1, "SCREEN"), (GoalKind.Guard, 1, "GUARD ME"),
        (GoalKind.Reserve, 1, "RESERVE"), (GoalKind.Harass, 1, "HARASS"), (GoalKind.Pursue, 1, "PURSUE"), (GoalKind.Ambush, 1, "AMBUSH"),
    };

    /// The goal wheel's sectors (field view), clockwise from the top: the same count as the order wheel.
    public static readonly (GoalKind G, int Side, string Label)[] GoalWheelOrder =
    {
        (GoalKind.Attack, 1, "ATTACK"), (GoalKind.Take, 1, "TAKE GROUND"), (GoalKind.Flank, 1, "FLANK RIGHT"), (GoalKind.Pursue, 1, "PURSUE"),
        (GoalKind.FallBack, 1, "FALL BACK"), (GoalKind.Flank, -1, "FLANK LEFT"), (GoalKind.Hold, 1, "HOLD"), (GoalKind.Guard, 1, "GUARD ME"),
    };

    void LayoutDivisions()
    {
        divCards.Clear();
        goals.Clear();
        // Right, under the minimap: the battle log is top left and the score bar in the middle.
        int w = U(200), h = U(46), x = W - w - U(10), y = minimap.Bottom + U(22);
        foreach (var d in B.DivisionsOf(0))
        {
            divCards.Add((new Rectangle(x, y, w, h), d));
            y += h + U(4);
        }
    }

    void LayoutGoals()
    {
        int bw = U(104), bh = U(21), gap = U(3), n = GoalButtons.Length;
        int x0 = W - 2 * bw - gap - U(14), y0 = H - (n + 1) / 2 * (bh + gap) - U(26);
        for (int i = 0; i < n; i++)
            goals.Add((new Rectangle(x0 + (i % 2) * (bw + gap), y0 + (i / 2) * (bh + gap), bw, bh), GoalButtons[i].G, GoalButtons[i].Side));
        int right = x0 + 2 * bw + gap;
        goalClose = new Rectangle(right - U(40), y0 - U(32), U(40), U(13));
        hornToggle = new Rectangle(right - U(92), y0 - U(18), U(92), U(13)); // the second row, beside the RMB hint
    }

    public Division? DivisionAt(Point p) => divCards.FirstOrDefault(c => c.R.Contains(p)).D;
    public (GoalKind G, int Side)? GoalAt(Point p) => goals.Where(g => g.R.Contains(p)).Select(g => ((GoalKind, int)?)(g.G, g.Side)).FirstOrDefault();
    public bool GoalCloseAt(Point p) => Div != null && Command && goalClose.Contains(p);

    readonly Dictionary<string, string> divTagCache = new();
    /// A division's letter on its companies' flags: the first letter of its name, skipping "The".
    string DivisionTag(Division d)
    {
        if (divTagCache.TryGetValue(d.Name, out var tag)) return tag;
        var words = d.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var w = words.Length > 1 && words[0].Equals("the", StringComparison.OrdinalIgnoreCase) ? words[1] : words.FirstOrDefault() ?? "?";
        return divTagCache[d.Name] = w[..1].ToUpperInvariant();
    }

    static string GoalTag(Goal g) => g.Kind == GoalKind.Flank ? (g.Side > 0 ? "FLANK R" : "FLANK L") : Goal.Label(g.Kind);

    void DivisionCards()
    {
        if (divCards.Count == 0) return;
        Text($"SERGEANTS ({Ctl.Name(Act.NextDivision)})", divCards[0].R.X, divCards[0].R.Y - U(12), Gold * 0.9f, 1);
        foreach (var (r, d) in divCards)
        {
            bool sel = d == Div, hover = r.Contains(Mouse);
            int alive = d.Alive, start = Math.Max(1, d.Start);
            int morale = AvgMorale(d.Squads);
            var hue = DivisionColour(d);
            Panel(r, sel ? Gold : d.Leaderless ? Bad : d.Query != null ? Warn : hover && Command ? Color.White * 0.5f : Edge, sel ? 2 : 1);
            Rect(new Rectangle(r.X + U(1), r.Y + U(1), U(3), r.Height - U(2)), hue); // his colour, as on the field
            if (alive == 0 && d.Squads.Count > 0) Rect(r, Color.Black * 0.4f);
            int x = r.X + U(8);
            // Head: the division, and its men (or ADD while you deploy with companies picked).
            var add = divAdds.FirstOrDefault(a => a.D == d).R;
            string count = $"{alive}/{d.Start}";
            Text(kit.Fit(d.Name.ToUpperInvariant(), r.Right - x - (add.IsEmpty ? TW(count, 1) : add.Width) - U(10)), x, r.Y + U(4), sel ? Gold : Color.White, 1);
            if (!add.IsEmpty) SmallButton(add, $"ADD {Movable(d)}", hue);
            else TextR(count, r.Right - U(5), r.Y + U(4), sel ? Gold : Color.White, 1);
            // What he is doing, and the goal (or the rider on his way).
            string report = B.Report(d);
            var rc = report switch { "SERGEANT DOWN" or "BROKEN" => Bad, "ASKS FOR ORDERS" or "UNDER ARROWS" => Warn, "ENGAGED" => Orange, _ => Dim };
            string goal = d.Pending != null ? $"> {GoalTag(d.Pending)} {MathF.Max(0, d.PendingAt - B.Time):0}S" : GoalTag(d.Goal);
            TextR(goal, r.Right - U(5), r.Y + U(15), d.Pending != null ? Warn : Color.White * 0.8f, 1);
            Text(kit.Fit(report, r.Right - x - TW(goal, 1) - U(12)), x, r.Y + U(15), rc, 1);
            // His skill as pips, his nature, and the horn plan or that he can hear you.
            for (int i = 0; i < 10; i++) Rect(new Rectangle(x + i * U(4), r.Y + U(27), U(3), U(4)), i < d.Skill ? Gold : Color.White * 0.15f);
            string tail = d.Horn != null ? $"HORN: {GoalTag(d.Horn)}" : d.PendingHorn != null ? "PLAN RIDING" : B.InVoice(d) && !Deploying ? "IN EARSHOT" : "";
            if (tail.Length > 0) TextR(tail, r.Right - U(5), r.Y + U(26), d.Horn != null ? Gold : d.PendingHorn != null ? Warn : Ok, 1);
            string nature = d.Traits.Length > 0 ? d.Traits[0].ToUpperInvariant() : d.Temper.ToString().ToUpperInvariant();
            Text(kit.Fit(nature, r.Right - x - U(44) - TW(tail, 1) - U(10)), x + U(44), r.Y + U(26), Dim, 1);
            Bar(new Rectangle(x, r.Y + U(38), (r.Right - x) / 2 - U(6), U(3)), alive / (float)start, Friendly);
            Bar(new Rectangle(x + (r.Right - x) / 2, r.Y + U(38), (r.Right - x) / 2 - U(5), U(3)), morale / 100f, MoraleColor(morale));
        }
    }

    static string GoalExplain(GoalKind g, int side) => g switch
    {
        GoalKind.Attack => "CHARGE AND PRESS THE CHOSEN OR NEAREST ENEMY",
        GoalKind.Hold => "HOLD GROUND; FIGHT FOES THAT COME CLOSE",
        GoalKind.Take => "ADVANCE TO THE SPOT (OR RMB ON LAND) AND HOLD IT",
        GoalKind.FallBack => "WITHDRAW FROM THE FIGHT AND RE-FORM BEHIND",
        GoalKind.Flank => side < 0 ? "SWING AROUND THE ENEMY'S LEFT FLANK" : "SWING AROUND THE ENEMY'S RIGHT FLANK",
        GoalKind.Screen => "GUARD THE CHOSEN (RMB) OR NEAREST ALLIED COMPANY",
        GoalKind.Guard => "STAY CLOSE AND PROTECT YOUR CAPTAIN",
        GoalKind.Reserve => "WAIT BEHIND THE LINE; COMMIT WHEN A FLANK FALTERS",
        GoalKind.Harass => "SKIRMISH AND KEEP DISTANCE WITH BOWS OR HORSE",
        GoalKind.Pursue => "RUN DOWN BROKEN AND ROUTING ENEMIES",
        GoalKind.Ambush => "HIDE IN THE NEAREST WOOD AND STRIKE FROM COVER",
        _ => "",
    };

    void GoalPanel()
    {
        if (goals.Count == 0 || Div == null) return;
        var first = goals[0].R;
        int right = goals[1].R.Right;
        var area = new Rectangle(first.X - U(6), first.Y - U(36), right - first.X + U(12), goals[^1].R.Bottom - first.Y + U(54));
        Panel(area, Gold);
        Text(kit.Fit(HornMode ? $"ON THE HORN: {Div.Name.ToUpperInvariant()}" : $"SERGEANT: {Div.Name.ToUpperInvariant()}", goalClose.X - area.X - U(10)), area.X + U(6), area.Y + U(6), Gold, 1);
        SmallButton(hornToggle, HornMode ? "HORN PLAN: ON" : "HORN PLAN: OFF", HornMode ? Warn : Dim);
        bool backHover = goalClose.Contains(Mouse);
        Rect(goalClose, backHover ? Gold * 0.35f : Color.White * 0.08f);
        Frame(goalClose, backHover ? Gold : Dim, U(1));
        TextC("BACK", goalClose.Center.X, goalClose.Y + U(3), backHover ? Gold : Color.White, 1);
        Text(kit.Fit("RMB AIMS IT", hornToggle.X - area.X - U(10)), area.X + U(6), area.Y + U(21), Dim, 1);
        (GoalKind G, int Side)? hovered = null;
        foreach (var (r, g, side) in goals)
        {
            var mark = HornMode ? Div.Horn ?? Div.PendingHorn : Div.Goal;
            bool hover = r.Contains(Mouse), on = mark != null && mark.Kind == g && (g != GoalKind.Flank || mark.Side == side);
            if (hover) hovered = (g, side);
            Rect(r, hover ? Gold * 0.35f : on ? Gold * 0.15f : Color.White * 0.06f);
            Frame(r, hover || on ? Gold : Edge, U(1));
            Text(GoalButtons.First(b => b.G == g && b.Side == side).Label, r.X + U(6), r.Y + U(7), Color.White, 1);
        }
        Text(HornMode ? $"KEPT UNTIL THE HORN ({Ctl.Name(Act.Horn)})" : "BACK / CLICK CARD: ORDER SQUADS", area.X + U(6), area.Bottom - U(13), HornMode ? Gold : Dim, 1);
        if (hovered is var (hg, hs))
        {
            string tip = GoalExplain(hg, hs);
            int tw = TW(tip, 1) + U(14);
            var tr = new Rectangle(Math.Min(area.Right - tw, W - tw - U(6)), area.Y - U(22), tw, U(18));
            Panel(tr, Gold);
            TextC(tip, tr.Center.X, tr.Y + U(5), Color.White, 1);
        }
    }

    void DivisionTooltip()
    {
        if (divCards.FirstOrDefault(c => c.R.Contains(Mouse)) is not { R.Width: > 0 } hit) return;
        var d = hit.D;
        bool sel = d == Div;
        string who = Battle.SergeantName(d) is { Length: > 0 } nm ? $"{nm.ToUpperInvariant()}, " : "";
        string l1 = $"{d.Name.ToUpperInvariant()}  ({who}{d.Temper.ToString().ToUpperInvariant()}{(d.Traits.Length > 0 ? " " + string.Join(" ", d.Traits).ToUpperInvariant() : "")} SERGEANT, SKILL {d.Skill})";
        string l2 = $"GOAL: {d.Goal.Describe.ToUpperInvariant()}";
        string l3 = d.Pending != null ? $"RIDER EN ROUTE: {GoalTag(d.Pending)} ({MathF.Max(0, d.PendingAt - B.Time):0}S)" : $"{d.Squads.Count} COMPANIES   {d.Alive}/{d.Start} MEN";
        string l4 = sel ? $"CLICK CARD, BACK, OR {Ctl.Name(Act.SelectAll)} TO ORDER COMPANIES" : $"CLICK OR PRESS {Ctl.Name(Act.NextDivision)} TO COMMAND SERGEANT";
        int w = Math.Max(Math.Max(TW(l1, 1), TW(l2, 1)), Math.Max(TW(l3, 1), TW(l4, 1))) + U(16);
        int h = U(58);
        var tr = new Rectangle(Math.Max(U(8), hit.R.X - w - U(8)), Math.Min(hit.R.Y, H - h - U(8)), w, h);
        Panel(tr, sel ? Gold : Edge);
        Text(l1, tr.X + U(8), tr.Y + U(6), Gold, 1);
        Text(l2, tr.X + U(8), tr.Y + U(19), Color.White, 1);
        Text(l3, tr.X + U(8), tr.Y + U(31), d.Pending != null ? Warn : Dim, 1);
        Text(l4, tr.X + U(8), tr.Y + U(43), sel ? Gold : Ok, 1);
    }

    void SergeantFieldBadge()
    {
        if (Div == null) return;
        string s = $"SERGEANT: {Div.Name.ToUpperInvariant()}   {Ctl.Name(Act.OrderWheel)}: GOAL WHEEL   {Ctl.Name(Act.NextDivision)}: NEXT/EXIT";
        int w = TW(s, 1) + U(14), y = divCards.Count > 0 ? divCards[^1].R.Bottom + U(6) : minimap.Bottom + U(22);
        var r = new Rectangle(W - w - U(10), y, w, U(18));
        Panel(r, Gold);
        TextC(s, r.Center.X, r.Y + U(5), Gold, 1);
    }

    void GoalWheel()
    {
        Rect(new Rectangle(0, 0, W, H), Color.Black * 0.25f);
        var c = new Vector2(W / 2f, H / 2f);
        for (int i = 0; i < GoalWheelOrder.Length; i++)
        {
            float a = -MathHelper.PiOver2 + i * MathHelper.TwoPi / GoalWheelOrder.Length;
            var at = c + new Vector2(MathF.Cos(a), MathF.Sin(a)) * U(130);
            var r = new Rectangle((int)at.X - U(58), (int)at.Y - U(11), U(116), U(22));
            bool on = i == WheelSector;
            Panel(r, on ? Gold : Edge, on ? 2 : 1);
            if (on) Rect(r, Gold * 0.25f);
            TextC(GoalWheelOrder[i].Label, at.X, at.Y - U(4), on ? Gold : Color.White, 1);
        }
        var inner = new Rectangle((int)c.X - U(70), (int)c.Y - U(16), U(140), U(32));
        Panel(inner, Gold);
        TextC("GOAL FOR", c.X, c.Y - U(10), Dim, 1);
        string n = Div!.Name.ToUpperInvariant();
        TextC(n.Length > 18 ? n[..18] : n, c.X, c.Y + U(1), Gold, 1);
    }

    /// A small pennant over every sergeant who leads a division (yours gold, the enemy's red); in field view only near you.
    void SergeantPennants()
    {
        var vp = new Viewport(0, 0, W, H);
        foreach (var d in B.Divisions)
        {
            if (d.Leader is not { Active: true } l) continue;
            if (!Command && Vector2.DistanceSquared(l.Pos, B.Player.Pos) > 70 * 70) continue;
            var v = vp.Project(new Vector3(l.Pos.X, B.Terrain.Height(l.Pos) + (l.Mounted ? 3.6f : 2.6f), l.Pos.Y), Proj, View, Matrix.Identity);
            if (v.Z is <= 0 or >= 1) continue;
            int x = (int)v.X, y = (int)v.Y, s = Command ? U(1) : Math.Clamp((int)(U(1) * 30 / MathF.Max(8, Vector2.Distance(l.Pos, B.Player.Pos))), 1, U(2));
            var col = d.Team == 0 ? Gold : Foe;
            Rect(new Rectangle(x, y - 12 * s, Math.Max(1, s), 12 * s), Color.Black * 0.7f); // staff
            for (int i = 0; i < 5; i++) Rect(new Rectangle(x + s, y - 12 * s + i * s, (8 - i * 1) * s, s), col); // a tapering pennant
        }
    }

    /// Your named men (docs/archive/LEGACY.md L2): a small gold mark over each within 25 m of you (any distance in command view), and
    /// his name when he is close (10 m) or under the cursor.
    void NamedMarks()
    {
        var vp = new Viewport(0, 0, W, H);
        foreach (var u in B.Units)
        {
            if (u.SoldierId == 0 || u.Team != 0 || !u.Active) continue;
            float d = Vector2.Distance(u.Pos, B.Player.Pos);
            if (!Command && d > 25) continue;
            var v = vp.Project(new Vector3(u.Pos.X, B.Terrain.Height(u.Pos) + (u.Mounted ? 3.2f : 2.2f), u.Pos.Y), Proj, View, Matrix.Identity);
            if (v.Z is <= 0 or >= 1) continue;
            int x = (int)v.X, y = (int)v.Y, s = Command ? U(1) : Math.Clamp((int)(U(1) * 20 / MathF.Max(6, d)), 1, U(2));
            Rect(new Rectangle(x - s, y - 3 * s, 2 * s, 2 * s), Color.Black * 0.6f);
            Rect(new Rectangle(x - s, y - 4 * s, 2 * s, 2 * s), Gold);
            bool hover = Command && Math.Abs(Mouse.X - x) < U(8) && Math.Abs(Mouse.Y - y) < U(12);
            if ((!Command && d < 10 || hover) && u.Squad?.Company?.Named?.FirstOrDefault(m => m.Id == u.SoldierId) is NamedSpec m)
            {
                TextC(m.Name.ToUpperInvariant(), x, y - 4 * s - U(10), u.Routing ? Dim : Gold, 1);
                // His small face beside his name (C1), the same face the roll shows.
                var face = PortraitPainter.OfMan(m.Id, u.Type, m.Wounds, m.Age > 0 ? m.Age : 25, false, 0, u.Routing ? 2 : 1);
                var tex = Portraits.Of(sb.GraphicsDevice, face, half: true);
                int fw = U(12), fh = U(15), fx = x - TW(m.Name.ToUpperInvariant(), 1) / 2 - fw - U(4), fy = y - 4 * s - U(14);
                Rect(new Rectangle(fx - 1, fy - 1, fw + 2, fh + 2), Color.Black * 0.7f);
                sb.Draw(tex, new Rectangle(fx, fy, fw, fh), u.Routing ? new Color(130, 128, 120) : Color.White);
            }
        }
        // Fallen named men you can still get out (L8): a red mark, and close by, what to do and how far along you are.
        foreach (var u in B.Downed)
        {
            if (!B.Rescuable(u)) continue;
            float d = Vector2.Distance(u.Pos, B.Player.Pos);
            if (!Command && d > 25) continue;
            var v = vp.Project(new Vector3(u.Pos.X, B.Terrain.Height(u.Pos) + 0.8f, u.Pos.Y), Proj, View, Matrix.Identity);
            if (v.Z is <= 0 or >= 1) continue;
            int x = (int)v.X, y = (int)v.Y, s = Command ? U(1) : Math.Clamp((int)(U(1) * 20 / MathF.Max(6, d)), 1, U(2));
            Rect(new Rectangle(x - s, y - 4 * s, 2 * s, 2 * s), Foe);
            if (d < 8 && !Command)
            {
                TextC($"{Battle.NameOf(u).ToUpperInvariant()} IS DOWN: STAND OVER HIM", x, y - 4 * s - U(10), Foe, 1);
                if (u.Rescue > 0) Bar(new Rectangle(x - U(20), y - 4 * s - U(18), U(40), U(3)), u.Rescue / Battle.RescueTime, Gold);
            }
        }
    }

    /// Your banner (P6): a tall gold pennant over its bearer, or a fallen grey one on the ground where it lies.
    void BannerMark()
    {
        if (!B.Options.Banner) return;
        var vp = new Viewport(0, 0, W, H);
        var at = B.BannerDown ? B.BannerPos : B.Bearer is { Active: true } b ? b.Pos : (Vector2?)null;
        if (at is not Vector2 p) return;
        float lift = B.BannerDown ? 0.3f : B.Bearer!.Mounted ? 4.6f : 3.6f;
        var v = vp.Project(new Vector3(p.X, B.Terrain.Height(p) + lift, p.Y), Proj, View, Matrix.Identity);
        if (v.Z is <= 0 or >= 1) return;
        int x = (int)v.X, y = (int)v.Y, s = Command ? U(1) : Math.Clamp((int)(U(2) * 30 / MathF.Max(8, Vector2.Distance(p, B.Player.Pos))), U(2), U(3));
        if (B.BannerDown)
        {
            Rect(new Rectangle(x - 8 * s, y, 16 * s, Math.Max(1, s)), Color.Black * 0.7f); // the staff in the mud
            Rect(new Rectangle(x - 2 * s, y - 4 * s, 7 * s, 4 * s), Dim);
            TextC("YOUR BANNER", x, y + 3 * s, Dim, 1);
            return;
        }
        Rect(new Rectangle(x, y - 16 * s, Math.Max(1, s), 16 * s), Color.Black * 0.7f);
        Rect(new Rectangle(x + s, y - 16 * s, 9 * s, 6 * s), Gold);
        Rect(new Rectangle(x + s, y - 10 * s, 5 * s, 2 * s), Gold);
    }

    /// In command view: a line from each of your divisions to where its goal points (dim when a new goal is on its way).
    /// Where a horn plan that names no spot points: round the flank or at the foe, near enough for a line on the map.
    Vector2? HornAim(Division d, Goal h)
    {
        if (h.Target?.Alive > 0) return h.Target.Center;
        if (h.Kind is not (GoalKind.Flank or GoalKind.Attack) || B.Squads.All(s => s.Team == d.Team || s.Alive == 0)) return null;
        var foe = B.Centroid(1 - d.Team);
        if (h.Kind == GoalKind.Attack) return foe;
        var f = foe - B.Centroid(d.Team);
        if (f.LengthSquared() < 1) return foe;
        f.Normalize();
        return foe + new Vector2(-f.Y, f.X) * (h.Side * 35);
    }

    void DivisionMarkers()
    {
        var vp = new Viewport(0, 0, W, H);
        bool Screen(Vector2 p, out Vector2 s)
        {
            var v = vp.Project(new Vector3(p.X, B.Terrain.Height(p) + 1, p.Y), Proj, View, Matrix.Identity);
            s = new Vector2(v.X, v.Y);
            return v.Z is > 0 and < 1;
        }
        // Riders on the road: yours gold, the enemy's red (cut one down and his order comes late).
        foreach (var c in B.Couriers)
            if (Screen(c.Pos, out var cp))
            {
                var col = c.Div.Team == 0 ? Gold : Foe;
                Rect(new Rectangle((int)cp.X - U(3), (int)cp.Y - U(3), U(6), U(6)), Color.Black * 0.7f);
                Rect(new Rectangle((int)cp.X - U(2), (int)cp.Y - U(2), U(4), U(4)), col);
                // A little flag on a staff that flutters as he rides, so you can watch the order travel.
                Rect(new Rectangle((int)cp.X, (int)cp.Y - U(14), Math.Max(1, U(1)), U(12)), Color.Black * 0.7f);
                for (int i = 0; i < 4; i++) Rect(new Rectangle((int)cp.X + U(1), (int)cp.Y - U(14) + i * U(1) + (int)(MathF.Sin(Ironfield.Ui.Now * 12 + i) * U(0.6f)), U(7 - i), Math.Max(1, U(1))), col);
                if (c.Div.Team == 0) Text("RIDER", cp.X + U(5), cp.Y - U(4), col, 1);
            }
        divTags.Clear();
        foreach (var d in B.DivisionsOf(0))
        {
            if (d.Alive == 0) continue;
            var sum = Vector2.Zero;
            int liveCount = 0;
            foreach (var s in d.Squads) if (s.Alive > 0) { sum += s.Center; liveCount++; }
            if (liveCount == 0) continue;
            var at = sum / liveCount;
            if (!Screen(at, out var from)) continue;
            var col = d == Div ? Gold : DivisionColour(d);
            for (int gi = 0; gi < 2; gi++)
            {
                var g = gi == 0 ? d.Goal : d.Pending;
                if (g == null) continue;
                Vector2? to = g.Kind is GoalKind.Hold or GoalKind.Take or GoalKind.FallBack or GoalKind.Ambush ? g.Point : g.Target?.Alive > 0 ? g.Target.Center : null;
                if (to is Vector2 t && Screen(t, out var dst))
                {
                    var c = g == d.Pending ? Warn * 0.7f : col * 0.8f;
                    Line(from, dst, c, U(2));
                    Rect(new Rectangle((int)dst.X - U(3), (int)dst.Y - U(3), U(6), U(6)), c);
                }
            }
            // The horn plan: a dotted line in his colour to where the horn will send him.
            if ((d.Horn ?? d.PendingHorn) is { } h && (h.Kind is GoalKind.Hold or GoalKind.Take or GoalKind.FallBack or GoalKind.Ambush ? h.Point : HornAim(d, h)) is Vector2 ht && Screen(ht, out var hd))
            {
                var hc = DivisionColour(d);
                var step = hd - from; float len = step.Length();
                for (float t0 = 0; t0 < len; t0 += U(10)) Line(from + step * (t0 / len), from + step * (MathF.Min(len, t0 + U(5)) / len), hc, U(2));
                // The end may lie off the screen: the label goes where the route leaves it, and none if no route shows.
                var room = new Rectangle(U(30), U(104), W - U(60), H - U(234));
                bool In(Vector2 v) => room.Contains((int)v.X, (int)v.Y);
                if (In(from))
                {
                    float t1 = 1;
                    if (!In(hd)) { float a = 0, b2 = 1; for (int k = 0; k < 14; k++) { float m = (a + b2) / 2; if (In(from + step * m)) a = m; else b2 = m; } t1 = a; }
                    var lp = from + step * t1 + new Vector2(0, U(4));
                    var lr = new Rectangle((int)lp.X - TW("HORN", 1) / 2 - U(3), (int)lp.Y - U(2), TW("HORN", 1) + U(6), U(12));
                    Rect(lr, Color.Black * 0.6f);
                    TextC("HORN", lp.X, lp.Y, hc, 1);
                }
            }
            string tag = (d.Leader is { Active: true } ? "" : "! ") + d.Name.ToUpperInvariant() + ": " + GoalTag(d.Goal);
            // The tag stays off the HUD's panels, and one that would cover another division's is dimmed.
            int tw = TW(tag, 1);
            from.X = Math.Clamp(from.X, tw / 2f + U(4), W - tw / 2f - U(4)); // keep the tag on the screen
            var tr = new Rectangle((int)from.X - tw / 2, (int)from.Y - U(54), tw, U(9)); // above the companies' flags
            var near = tr; near.Inflate(U(4), U(12)); // a margin, so a tag does not sit on a panel's heading either
            if (Over(near.Location) || Over(new Point(near.Center.X, near.Y)) || Over(new Point(near.Right, near.Y))
                || Over(new Point(near.X, near.Bottom)) || Over(new Point(near.Center.X, near.Bottom)) || Over(new Point(near.Right, near.Bottom))) continue;
            bool crowded = false;
            foreach (var o in divTags) if (o.Intersects(tr)) { crowded = true; break; }
            divTags.Add(tr);
            Rect(new Rectangle(tr.X - U(3), tr.Y - U(2), tr.Width + U(6), tr.Height + U(3)), Color.Black * (crowded ? 0.35f : 0.6f));
            TextC(tag, from.X, from.Y - U(54), crowded ? col * 0.55f : col, 1);
        }
    }
}
