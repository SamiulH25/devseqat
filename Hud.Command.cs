using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// Hud: the command tray (who you lead, the reserve, the horn), sergeants' questions, and each division's colour and
/// rings on the field (docs/SERGEANTS.md SG1-SG3, SG6).
sealed partial class Hud
{
    /// The goal you pick for the chosen sergeant becomes his horn plan (SG2).
    public bool HornMode;

    Rectangle tray, toMe, hornButton, hornToggle;
    readonly List<(Rectangle R, int I)> reserveChips = new();
    readonly List<(Rectangle R, Division D)> divAdds = new();
    readonly List<(Rectangle R, Division D, bool A)> answers = new();

    public int? ReserveAt(Point p) => reserveChips.Where(c => c.R.Contains(p)).Select(c => (int?)c.I).FirstOrDefault();
    public bool ToMeAt(Point p) => toMe.Contains(p);
    public bool HornAt(Point p) => hornButton.Contains(p);
    public bool HornToggleAt(Point p) => Div != null && Command && hornToggle.Contains(p);
    public Division? AddAt(Point p) => divAdds.FirstOrDefault(c => c.R.Contains(p)).D;
    public (Division D, bool A)? AnswerAt(Point p) => answers.Where(a => a.R.Contains(p)).Select(a => ((Division, bool)?)(a.D, a.A)).FirstOrDefault();
    bool OverCommand(Point p) => tray.Contains(p) || AnswerAt(p) != null || AddAt(p) != null || HornToggleAt(p);

    /// Each of your divisions in its own colour, by its place among your divisions (gold stays the colour of a choice).
    static readonly Color[] DivisionHues =
    {
        new(96, 196, 206), new(176, 136, 226), new(226, 150, 84), new(128, 204, 112), new(222, 122, 172), new(116, 164, 238),
    };
    Color DivisionColour(Division d)
    {
        int i = 0;
        foreach (var o in B.DivisionsOf(d.Team)) { if (o == d) break; i++; }
        return DivisionHues[i % DivisionHues.Length];
    }

    /// The one sergeant's question on screen: the oldest one waiting.
    public Division? Asking => B.DivisionsOf(0).Where(d => d.Query != null).OrderBy(d => d.Query!.Until).FirstOrDefault();

    /// Selected companies that could go to division d (or to you when d is null) before the battle.
    int Movable(Division? d) => Deploying ? Selected.Count(s => B.CannotAssign(s, d) == null) : 0;

    void LayoutCommand()
    {
        reserveChips.Clear(); divAdds.Clear(); answers.Clear();
        tray = toMe = hornButton = Rectangle.Empty;
        LayoutQuestion();
        if (!Command || ShowResult) return;
        foreach (var (r, d) in divCards)
            if (Movable(d) > 0) divAdds.Add((new Rectangle(r.Right - U(50), r.Y + U(3), U(46), U(12)), d));
        // The tray sits over your own panel: a head row, then the reserve's chips three to a row.
        var waiting = B.ReservesOf(0);
        int pending = B.CallUpsOf(0).Count(), chips = Math.Min(9, waiting.Count + pending);
        int rows = Math.Max(1, (chips + 2) / 3), h = U(30) + rows * U(18);
        tray = new Rectangle(player.X, player.Y - h - U(6), player.Width, h);
        int cw = (tray.Width - U(16)) / 3;
        for (int i = 0; i < Math.Min(chips, waiting.Count); i++)
            reserveChips.Add((new Rectangle(tray.X + U(6) + i % 3 * (cw + U(2)), tray.Y + U(18) + i / 3 * U(18), cw, U(15)), i));
        if (Deploying && Movable(null) > 0) toMe = new Rectangle(tray.Right - U(70), tray.Y + U(3), U(64), U(12));
        else if (!Deploying && B.DivisionsOf(0).Any(d => d.Horn != null && d.Alive > 0)) hornButton = new Rectangle(tray.Right - U(96), tray.Y + U(3), U(90), U(12));
    }

    void LayoutQuestion()
    {
        if (Asking is not { Query: { } q } d || ShowResult || MenuOpen) return;
        var box = QuestionBox;
        int bw = U(150), y = box.Y + U(22);
        answers.Add((new Rectangle(box.Center.X - bw - U(4), y, bw, U(16)), d, true));
        answers.Add((new Rectangle(box.Center.X + U(4), y, bw, U(16)), d, false));
    }
    Rectangle QuestionBox => new(W / 2 - U(230), Paused ? U(124) : U(92), U(460), U(62));

    /// Left, over your panel: how many companies you lead, the reserve (click to call one up), TO ME and the horn.
    void Tray()
    {
        if (tray.IsEmpty) return;
        Panel(tray, Edge);
        int led = B.LedCount(0);
        bool full = led >= Armies.CommandCap;
        Text($"COMMAND {led}/{Armies.CommandCap}", tray.X + U(6), tray.Y + U(5), full ? Gold : Color.White, 1);
        string sub = B.ReservesOf(0).Count + B.CallUpsOf(0).Count() == 0 ? "NO RESERVE" : Div != null ? $"FOR {Div.Name.ToUpperInvariant()}" : full ? "PICK A SERGEANT (Y)" : "CLICK: CALL UP";
        int subX = tray.X + U(14) + TW($"COMMAND {led}/{Armies.CommandCap}", 1);
        int subRoom = (!toMe.IsEmpty ? toMe.X : !hornButton.IsEmpty ? hornButton.X : tray.Right) - subX - U(6);
        Text(kit.Fit(sub, subRoom), subX, tray.Y + U(5), Dim, 1);
        var waiting = B.ReservesOf(0);
        foreach (var (r, i) in reserveChips)
        {
            var (c, _, _) = waiting[i];
            bool hover = r.Contains(Mouse), can = B.CannotCallUp(0, i, Div) == null;
            Rect(r, hover && can ? Gold * 0.3f : Color.Black * 0.35f);
            Frame(r, hover ? (can ? Gold : Bad) : Edge, U(1));
            var t = Troops.Get(c.Troop);
            string n = $"{c.Count}";
            Text(kit.Fit(t.Name.ToUpperInvariant(), r.Width - TW(n, 1) - U(12)), r.X + U(4), r.Y + U(4), can ? Color.White : Dim, 1);
            TextR(n, r.Right - U(4), r.Y + U(4), Dim, 1);
        }
        // Riders already gone back for a company: the chips after the reserve's, with the time left.
        int k = reserveChips.Count, cw = (tray.Width - U(16)) / 3;
        foreach (var (c, eta) in B.CallUpsOf(0))
        {
            if (k >= 9) break;
            var r = new Rectangle(tray.X + U(6) + k % 3 * (cw + U(2)), tray.Y + U(18) + k / 3 * U(18), cw, U(15));
            Frame(r, Warn * 0.7f, U(1));
            string n = $"IN {MathF.Max(0, eta):0}S";
            Text(kit.Fit(Troops.Get(c.Troop).Name.ToUpperInvariant(), r.Width - TW(n, 1) - U(12)), r.X + U(4), r.Y + U(4), Warn, 1);
            TextR(n, r.Right - U(4), r.Y + U(4), Warn, 1);
            k++;
        }
        int more = waiting.Count + B.CallUpsOf(0).Count() - k;
        if (more > 0) TextR($"+{more} MORE", tray.Right - U(6), tray.Bottom - U(9), Dim, 1);
        if (!toMe.IsEmpty) SmallButton(toMe, $"TO ME ({Movable(null)})", Gold);
        if (!hornButton.IsEmpty) SmallButton(hornButton, $"HORN ({Ctl.Name(Act.Horn)})", Gold);
    }

    void SmallButton(Rectangle r, string label, Color c)
    {
        bool hover = r.Contains(Mouse);
        Rect(r, hover ? c * 0.4f : c * 0.15f);
        Frame(r, c, U(1));
        TextC(label, r.Center.X, r.Y + U(3), hover ? Color.White : c, 1);
    }

    /// A sergeant's question, top centre in both views: what he sees, two answers, and what he does if you are silent.
    void Question()
    {
        if (answers.Count < 2 || answers[0].D.Query is not { } q) return;
        var d = answers[0].D;
        var box = QuestionBox;
        Panel(box, DivisionColour(d), 2);
        float left = MathF.Max(0, q.Until - B.Time);
        string head = $"{d.Name.ToUpperInvariant()}: {q.Text.ToUpperInvariant()}";
        Text(kit.Fit(head, box.Width - U(46)), box.X + U(8), box.Y + U(7), Color.White, 1);
        TextR($"{left:0}S", box.Right - U(8), box.Y + U(7), left < 4 ? Warn : Color.White, 1);
        for (int i = 0; i < 2; i++)
        {
            var (r, _, a) = answers[i];
            bool hover = r.Contains(Mouse), mine = a == q.AByDefault;
            Rect(r, hover ? Gold * 0.4f : Gold * 0.12f);
            Frame(r, hover ? Gold : Edge, U(1));
            TextC($"{Ctl.Name(a ? Act.AnswerA : Act.AnswerB)}: {(a ? q.LabelA : q.LabelB)}", r.Center.X, r.Y + U(5), hover ? Color.White : Gold, 1);
            if (mine) TextC("HIS OWN CHOICE", r.Center.X, r.Bottom + U(4), Dim, 1);
        }
        int others = B.DivisionsOf(0).Count(x => x.Query != null) - 1;
        if (others > 0) TextR($"+{others} ASKING", box.Right - U(8), box.Bottom - U(10), Warn, 1);
    }

    /// A footprint of the division's colour under each of its companies (command view): a box round the men's ranks, turned
    /// along its ranks, so you see at a glance who is whose without rings crossing the field.
    void DivisionRings()
    {
        var vp = new Viewport(0, 0, W, H);
        foreach (var d in B.DivisionsOf(0))
        {
            var col = (d == Div ? Gold : DivisionColour(d)) * (d == Div ? 0.9f : 0.6f);
            foreach (var s in d.Squads)
            {
                if (s.Alive == 0) continue;
                // The ranks' own lie (the long axis of the men), not the order's facing, and the stragglers left out.
                var men = s.Units.Where(u => !u.Dead).Select(u => u.Pos - s.Center).ToList();
                if (men.Count == 0) continue;
                float cxx = 0, cyy = 0, cxy = 0;
                foreach (var o in men) { cxx += o.X * o.X; cyy += o.Y * o.Y; cxy += o.X * o.Y; }
                float ang = 0.5f * MathF.Atan2(2 * cxy, cxx - cyy);
                var rt = new Vector2(MathF.Cos(ang), MathF.Sin(ang));
                var f = new Vector2(-rt.Y, rt.X);
                var along = men.Select(o => Vector2.Dot(o, rt)).OrderBy(v => v).ToList();
                var deep = men.Select(o => Vector2.Dot(o, f)).OrderBy(v => v).ToList();
                int lo = men.Count / 40, hi = men.Count - 1 - men.Count / 40;
                float minR = along[lo], maxR = along[hi], minF = deep[lo], maxF = deep[hi];
                const float pad = 1.8f;
                var corners = new[] { (minR - pad, minF - pad), (maxR + pad, minF - pad), (maxR + pad, maxF + pad), (minR - pad, maxF + pad) };
                Vector2? prev = null;
                foreach (var (a, b) in corners.Append(corners[0]))
                {
                    var p = s.Center + rt * a + f * b;
                    var v = vp.Project(new Vector3(p.X, B.Terrain.Height(p) + 0.3f, p.Y), Proj, View, Matrix.Identity);
                    if (v.Z is <= 0 or >= 1) { prev = null; continue; }
                    var sp = new Vector2(v.X, v.Y);
                    if (prev is Vector2 q) Line(q, sp, col, U(1));
                    prev = sp;
                }
            }
        }
    }
}
