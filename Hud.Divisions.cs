using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// Hud: your divisions (docs/COMBAT_OVERHAUL.md 1.7): a card per division above the player panel, the goal buttons that
/// replace the order buttons while one is picked, the goal wheel, and goal lines on the field in command view.
sealed partial class Hud
{
    readonly List<(Rectangle R, Division D)> divCards = new();
    readonly List<(Rectangle R, GoalKind G, int Side)> goals = new();

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
        var mine = B.DivisionsOf(0).ToList();
        // Right, under the minimap: the battle log is top left and the score bar in the middle.
        int w = U(200), h = U(34), x = W - w - U(10), y = minimap.Bottom + U(22);
        foreach (var d in mine)
        {
            divCards.Add((new Rectangle(x, y, w, h), d));
            y += h + U(4);
        }
    }

    void LayoutGoals()
    {
        int bw = U(112), bh = U(24), n = GoalButtons.Length;
        int x0 = W - 2 * bw - U(16), y0 = H - (n + 1) / 2 * (bh + U(4)) - U(10);
        for (int i = 0; i < n; i++)
            goals.Add((new Rectangle(x0 + (i % 2) * (bw + U(4)), y0 + (i / 2) * (bh + U(4)), bw, bh), GoalButtons[i].G, GoalButtons[i].Side));
    }

    public Division? DivisionAt(Point p) => divCards.FirstOrDefault(c => c.R.Contains(p)).D;
    public (GoalKind G, int Side)? GoalAt(Point p) => goals.Where(g => g.R.Contains(p)).Select(g => ((GoalKind, int)?)(g.G, g.Side)).FirstOrDefault();

    /// A division's letter on its companies' flags: the first letter of its name, skipping "The".
    static string DivisionTag(Division d)
    {
        var words = d.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var w = words.Length > 1 && words[0].Equals("the", StringComparison.OrdinalIgnoreCase) ? words[1] : words.FirstOrDefault() ?? "?";
        return w[..1].ToUpperInvariant();
    }

    static string GoalTag(Goal g) => g.Kind == GoalKind.Flank ? (g.Side > 0 ? "FLANK R" : "FLANK L") : Goal.Label(g.Kind);

    void DivisionCards()
    {
        if (divCards.Count == 0) return;
        Text($"DIVISIONS ({Ctl.Name(Act.NextDivision)})", divCards[0].R.X, divCards[0].R.Y - U(12), Dim, 1);
        foreach (var (r, d) in divCards)
        {
            bool sel = d == Div, hover = r.Contains(Mouse);
            int alive = d.Alive, start = Math.Max(1, d.Start);
            int morale = AvgMorale(d.Squads.SelectMany(s => s.Units));
            Panel(r, sel ? Gold : d.Leaderless ? Bad : hover && Command ? Color.White * 0.5f : Edge, sel ? 2 : 1);
            if (alive == 0) Rect(r, Color.Black * 0.4f);
            string name = d.Name.ToUpperInvariant();
            if (name.Length > 16) name = name[..16];
            Text(name, r.X + U(5), r.Y + U(4), sel ? Gold : Color.White, 1);
            TextR($"{alive}/{d.Start}", r.Right - U(5), r.Y + U(4), Color.White, 1);
            string line = d.Leaderless ? "NO ORDERS: SERGEANT DOWN" : d.Pending != null ? $"> {GoalTag(d.Pending)} IN {MathF.Max(0, d.PendingAt - B.Time):0}S" : GoalTag(d.Goal);
            Text(line, r.X + U(5), r.Y + U(15), d.Leaderless ? Bad : d.Pending != null ? Warn : Dim, 1);
            TextR(d.Temper.ToString().ToUpperInvariant(), r.Right - U(5), r.Y + U(15), Dim, 1);
            Bar(new Rectangle(r.X + U(5), r.Y + U(26), r.Width / 2 - U(8), U(3)), alive / (float)start, Friendly);
            Bar(new Rectangle(r.X + r.Width / 2 + U(3), r.Y + U(26), r.Width / 2 - U(8), U(3)), morale / 100f, MoraleColor(morale));
        }
    }

    void GoalPanel()
    {
        if (goals.Count == 0 || Div == null) return;
        var first = goals[0].R;
        var area = new Rectangle(first.X - U(6), first.Y - U(34), goals[^1].R.Right - first.X + U(12) + (goals.Count % 2 == 1 ? first.Width + U(4) : 0), goals[^1].R.Bottom - first.Y + U(40));
        Panel(area, Gold);
        Text($"GOAL FOR {Div.Name.ToUpperInvariant()}", area.X + U(6), area.Y + U(6), Gold, 1);
        Text("RMB: GROUND TAKES IT, AN ENEMY ATTACKS IT", area.X + U(6), area.Y + U(18), Dim, 1);
        foreach (var (r, g, side) in goals)
        {
            bool hover = r.Contains(Mouse), on = Div.Goal.Kind == g && (g != GoalKind.Flank || Div.Goal.Side == side);
            Rect(r, hover ? Gold * 0.35f : on ? Gold * 0.15f : Color.White * 0.06f);
            Frame(r, hover || on ? Gold : Edge, U(1));
            Text(GoalButtons.First(b => b.G == g && b.Side == side).Label, r.X + U(6), r.Y + U(8), Color.White, 1);
        }
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

    /// Your named men (docs/LEGACY.md L2): a small gold mark over each within 25 m of you (any distance in command view), and
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
                TextC(m.Name.ToUpperInvariant(), x, y - 4 * s - U(10), u.Routing ? Dim : Gold, 1);
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
                if (c.Div.Team == 0) Text("RIDER", cp.X + U(5), cp.Y - U(4), col, 1);
            }
        foreach (var d in B.DivisionsOf(0))
        {
            if (d.Alive == 0) continue;
            var live = d.Squads.Where(s => s.Alive > 0).ToList();
            var at = live.Aggregate(Vector2.Zero, (a, s) => a + s.Center) / live.Count;
            if (!Screen(at, out var from)) continue;
            var col = d == Div ? Gold : Friendly;
            foreach (var g in new[] { d.Goal, d.Pending })
            {
                if (g == null) continue;
                Vector2? to = g.Kind is GoalKind.Hold or GoalKind.Take or GoalKind.FallBack or GoalKind.Ambush ? g.Point : g.Target?.Alive > 0 ? g.Target.Center : null;
                if (to is Vector2 t && Screen(t, out var dst))
                {
                    var c = g == d.Pending ? Warn * 0.7f : col * 0.8f;
                    Line(from, dst, c, U(2));
                    Rect(new Rectangle((int)dst.X - U(3), (int)dst.Y - U(3), U(6), U(6)), c);
                }
            }
            string tag = (d.Leader is { Active: true } ? "" : "! ") + d.Name.ToUpperInvariant() + ": " + GoalTag(d.Goal);
            TextC(tag, from.X, from.Y - U(26), col, 1);
        }
    }
}
