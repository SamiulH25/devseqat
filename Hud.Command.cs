using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// Hud: the command tray (who you lead, the reserve, the horn) and each division's colour and rings on the field
/// (docs/archive/SERGEANTS.md SG1, SG2, SG6).
sealed partial class Hud
{
    /// The goal you pick for the chosen sergeant becomes his horn plan (SG2).
    public bool HornMode;

    Rectangle tray, toMe, hornButton, hornToggle, autoFill;
    readonly List<(Rectangle R, int I)> reserveChips = new();
    readonly List<(Rectangle R, Division D)> divAdds = new();

    public int? ReserveAt(Point p) => reserveChips.Where(c => c.R.Contains(p)).Select(c => (int?)c.I).FirstOrDefault();
    public bool ToMeAt(Point p) => toMe.Contains(p);
    public bool AutoFillAt(Point p) => autoFill.Contains(p);
    public bool HornAt(Point p) => hornButton.Contains(p);
    public bool HornToggleAt(Point p) => Div != null && Command && hornToggle.Contains(p);
    public Division? AddAt(Point p) => divAdds.FirstOrDefault(c => c.R.Contains(p)).D;
    bool OverCommand(Point p) => tray.Contains(p) || AddAt(p) != null || HornToggleAt(p) || AutoFillAt(p);

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

    /// Selected companies that could go to division d (or to you when d is null) before the battle.
    int Movable(Division? d) => Deploying ? Selected.Count(s => B.CannotAssign(s, d) == null) : 0;

    void LayoutCommand()
    {
        reserveChips.Clear(); divAdds.Clear();
        tray = toMe = hornButton = autoFill = Rectangle.Empty;
        if (!Command || ShowResult) return;
        foreach (var (r, d) in divCards)
            if (Movable(d) > 0) divAdds.Add((new Rectangle(r.Right - U(50), r.Y + U(2), U(46), U(12)), d));
        // The tray sits over your own panel: a head row, then the reserve's chips three to a row.
        var waiting = B.ReservesOf(0);
        int pending = B.CallUpsOf(0).Count(), chips = Math.Min(8, waiting.Count + pending);
        int rows = Math.Max(1, (chips + 1) / 2), h = U(30) + rows * U(18);
        tray = new Rectangle(player.X, player.Y - h - U(6), player.Width, h);
        int cw = (tray.Width - U(14)) / 2;
        for (int i = 0; i < Math.Min(chips, waiting.Count); i++)
            reserveChips.Add((new Rectangle(tray.X + U(6) + i % 2 * (cw + U(2)), tray.Y + U(18) + i / 2 * U(18), cw, U(15)), i));
        if (Deploying && Movable(null) > 0) toMe = new Rectangle(tray.Right - U(70), tray.Y + U(3), U(64), U(12));
        else if (Deploying && B.DivisionsOf(0).Any()) autoFill = new Rectangle(tray.Right - U(70), tray.Y + U(3), U(64), U(12)); // OF4
        else if (!Deploying && B.DivisionsOf(0).Any(d => d.Horn != null && d.Alive > 0)) hornButton = new Rectangle(tray.Right - U(96), tray.Y + U(3), U(90), U(12));
    }

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
        int subRoom = (!toMe.IsEmpty ? toMe.X : !hornButton.IsEmpty ? hornButton.X : !autoFill.IsEmpty ? autoFill.X : tray.Right) - subX - U(6);
        if (TW(sub, 1) <= subRoom) Text(sub, subX, tray.Y + U(5), Dim, 1); // whole or not at all: a cut hint reads as noise
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
        int k = reserveChips.Count, cw = (tray.Width - U(14)) / 2;
        foreach (var (c, eta) in B.CallUpsOf(0))
        {
            if (k >= 8) break;
            var r = new Rectangle(tray.X + U(6) + k % 2 * (cw + U(2)), tray.Y + U(18) + k / 2 * U(18), cw, U(15));
            Frame(r, Warn * 0.7f, U(1));
            string n = $"{MathF.Max(0, eta):0}S";
            Text(kit.Fit(Troops.Get(c.Troop).Name.ToUpperInvariant(), r.Width - TW(n, 1) - U(12)), r.X + U(4), r.Y + U(4), Warn, 1);
            TextR(n, r.Right - U(4), r.Y + U(4), Warn, 1);
            k++;
        }
        int more = waiting.Count + B.CallUpsOf(0).Count() - k;
        if (more > 0) TextR($"+{more} MORE", tray.Right - U(6), tray.Bottom - U(9), Dim, 1);
        if (!toMe.IsEmpty) SmallButton(toMe, $"TO ME ({Movable(null)})", Gold);
        if (!autoFill.IsEmpty) SmallButton(autoFill, "AUTO-FILL", Color.White * 0.85f); // a one-shot button, not a lit toggle
        if (!hornButton.IsEmpty) SmallButton(hornButton, $"HORN ({Ctl.Name(Act.Horn)})", Gold);
    }

    void SmallButton(Rectangle r, string label, Color c)
    {
        bool hover = r.Contains(Mouse);
        Rect(r, hover ? c * 0.4f : c * 0.15f);
        Frame(r, c, U(1));
        TextC(label, r.Center.X, r.Y + U(3), hover ? Color.White : c, 1);
    }

    readonly List<Vector2> ringMen = new(64);
    readonly List<float> ringAlong = new(64), ringDeep = new(64);

    /// A footprint of the division's colour under each of its companies (command view): a box round the men's ranks, turned
    /// along its ranks, so you see at a glance who is whose without rings crossing the field.
    void DivisionRings()
    {
        var vp = new Viewport(0, 0, W, H);
        Span<(float A, float B)> corners = stackalloc (float, float)[5];
        foreach (var d in B.DivisionsOf(0))
        {
            var col = (d == Div ? Gold : DivisionColour(d)) * (d == Div ? 0.9f : 0.6f);
            if (d == Div && d.Post != Post.Front && ZonesShown(d)) col = DivisionColour(d) * 0.6f; // the gold dashed zone is the mark; the ring stays his colour
            foreach (var s in d.Squads)
            {
                if (s.Alive == 0) continue;
                // The ranks' own lie (the long axis of the men), not the order's facing, and the stragglers left out.
                ringMen.Clear();
                var units = s.Units;
                var sc = s.Center;
                for (int i = 0; i < units.Count; i++) if (!units[i].Dead) ringMen.Add(units[i].Pos - sc);
                int mc = ringMen.Count;
                if (mc == 0) continue;
                float cxx = 0, cyy = 0, cxy = 0;
                for (int i = 0; i < mc; i++) { var o = ringMen[i]; cxx += o.X * o.X; cyy += o.Y * o.Y; cxy += o.X * o.Y; }
                float ang = 0.5f * MathF.Atan2(2 * cxy, cxx - cyy);
                var rt = new Vector2(MathF.Cos(ang), MathF.Sin(ang));
                var f = new Vector2(-rt.Y, rt.X);
                ringAlong.Clear(); ringDeep.Clear();
                for (int i = 0; i < mc; i++) { var o = ringMen[i]; ringAlong.Add(Vector2.Dot(o, rt)); ringDeep.Add(Vector2.Dot(o, f)); }
                ringAlong.Sort(); ringDeep.Sort();
                int lo = mc / 40, hi = mc - 1 - mc / 40;
                float minR = ringAlong[lo], maxR = ringAlong[hi], minF = ringDeep[lo], maxF = ringDeep[hi];
                const float pad = 1.8f;
                corners[0] = (minR - pad, minF - pad);
                corners[1] = (maxR + pad, minF - pad);
                corners[2] = (maxR + pad, maxF + pad);
                corners[3] = (minR - pad, maxF + pad);
                corners[4] = corners[0];
                Vector2? prev = null;
                for (int i = 0; i < 5; i++)
                {
                    var (a, b) = corners[i];
                    var p = sc + rt * a + f * b;
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
